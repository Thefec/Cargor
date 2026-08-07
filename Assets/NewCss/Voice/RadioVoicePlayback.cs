using System;
using System.Collections.Generic;
using UnityEngine;
using NewCss.Voice.Core;

/// <summary>
/// Konuşmacı slot havuzu + clientId → slot yönlendirmesi.
///
/// AĞDAN BAĞIMSIZ: kim çağırırsa çağırsın (loopback, disk replay, Dalga 3'te CustomMessagingManager
/// relay'i) aynı <see cref="HandleIncomingPacket"/> giriş noktasından geçer — bu, Adım 3-5'in ağ
/// kodu yazılmadan uçtan uca (loopback ile) doğrulanabilmesinin sebebi.
///
/// <see cref="RadioVoiceRuntime.SelfMonitorClientId"/> / <see cref="RadioVoiceRuntime.DevReplayClientId"/>
/// gibi rezerve pseudo-ID'ler gerçek NGO clientId'leriyle ÇAKIŞMAZ (NGO clientId'leri küçük,
/// host-atanan tam sayılardır, ulong.MaxValue ve MaxValue-1'e asla ulaşmazlar) — Dalga 3 bu
/// sabitlere dokunmadan kendi gerçek clientId'lerini aynı havuza güvenle sokabilir.
/// </summary>
public sealed class RadioVoicePlayback
{
    private readonly RadioVoiceSpeakerSlot[] _pool;
    private readonly Dictionary<ulong, RadioVoiceSpeakerSlot> _bySpeaker = new();

    private bool _warnedPoolExhaustedThisSession;

    public RadioVoicePlayback(RadioVoiceSpeakerSlot[] pool)
    {
        _pool = pool ?? throw new ArgumentNullException(nameof(pool));
    }

    /// <summary>AudioSettings.OnAudioConfigurationChanged sonrası tüm slotları yeni çıkış hızıyla yeniden kurar.</summary>
    public void Reconfigure(int sampleRate)
    {
        foreach (var slot in _pool) slot.Reconfigure(sampleRate);
    }

    /// <summary>Her frame: burst'ü zaman aşımına uğramış (400ms sessizlik — bkz. VoiceSequenceTracker.IsBurstStale)
    /// slotları serbest bırakır. BurstEnd bayrağı unreliable'da kaybolabileceği için bu, tek güvenilir yol.</summary>
    public void Tick(double now)
    {
        foreach (var slot in _pool)
        {
            if (slot.AssignedSpeakerId.HasValue && slot.Tracker.IsBurstStale(now))
            {
                FreeSlot(slot);
            }
        }
    }

    /// <summary>
    /// Ağdan bağımsız tek giriş noktası. <paramref name="fullPacket"/> = 4B VoicePacket başlığı +
    /// sıkıştırılmış ses (RadioVoiceCapture'ın ürettiği format, aynen). Bozuk/bilinmeyen versiyon
    /// paketleri sessizce düşer (VoicePacket sözleşmesi — exception yok).
    /// </summary>
    public void HandleIncomingPacket(ulong speakerId, ArraySegment<byte> fullPacket)
    {
        if (!VoicePacket.TryReadHeader(fullPacket.Array, fullPacket.Offset, fullPacket.Count, out var flags, out var sequence))
            return;

        var slot = GetOrAssignSlot(speakerId);
        if (slot == null) return; // havuz dolu, log-once GetOrAssignSlot içinde

        var payload = new ArraySegment<byte>(
            fullPacket.Array,
            fullPacket.Offset + VoicePacket.HeaderSize,
            fullPacket.Count - VoicePacket.HeaderSize);

        slot.SubmitPacket(flags, sequence, payload, Time.unscaledTimeAsDouble);
    }

    /// <summary>
    /// Dalga 3/10 için hazır arayüz: bir oyuncunun bağlantısı kesildiğinde 400ms'lik zaman aşımını
    /// beklemeden slotu anında serbest bırakmak içindir (plan: "konuşan disconnect → slot serbest +
    /// ~15ms fade-out"). Bu dalgada HİÇBİR YERDEN ÇAĞRILMIYOR (ağ kodu yok) — network katmanı
    /// eklenince OnClientDisconnect callback'i buraya tek satırla bağlanabilir.
    /// </summary>
    public void ForceReleaseSlot(ulong speakerId)
    {
        if (_bySpeaker.TryGetValue(speakerId, out var slot))
        {
            FreeSlot(slot);
        }
    }

    private RadioVoiceSpeakerSlot GetOrAssignSlot(ulong speakerId)
    {
        if (_bySpeaker.TryGetValue(speakerId, out var existing)) return existing;

        foreach (var slot in _pool)
        {
            if (!slot.AssignedSpeakerId.HasValue)
            {
                Assign(slot, speakerId);
                return slot;
            }
        }

        // Havuz dolu (>3 eşzamanlı konuşmacı, nadir): durgun/zaman aşımına uğramış bir slottan birini çal.
        double now = Time.unscaledTimeAsDouble;
        foreach (var slot in _pool)
        {
            if (slot.Tracker.IsBurstStale(now))
            {
                FreeSlot(slot);
                Assign(slot, speakerId);
                return slot;
            }
        }

        if (!_warnedPoolExhaustedThisSession)
        {
            Debug.LogWarning("[RadioVoicePlayback] Konuşmacı slot havuzu dolu (>3 eşzamanlı) — yeni konuşmacı düşürüldü. (oturum başına bir kez loglanır)");
            _warnedPoolExhaustedThisSession = true;
        }
        return null;
    }

    private void Assign(RadioVoiceSpeakerSlot slot, ulong speakerId)
    {
        slot.AssignedSpeakerId = speakerId;
        _bySpeaker[speakerId] = slot;
    }

    private void FreeSlot(RadioVoiceSpeakerSlot slot)
    {
        if (slot.AssignedSpeakerId.HasValue)
        {
            _bySpeaker.Remove(slot.AssignedSpeakerId.Value);
        }
        slot.Tracker.ResetBurst();
        slot.NotifySpeakerEnded();
    }
}
