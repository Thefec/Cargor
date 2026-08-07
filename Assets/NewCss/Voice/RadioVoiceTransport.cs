using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using NewCss.Voice.Core;

/// <summary>
/// Telsiz ses paketlerinin ağ taşıması: <see cref="CustomMessagingManager"/> üzerinden isimli
/// mesaj + server-authoritative relay, <see cref="NetworkDelivery.Unreliable"/>.
///
/// [Rpc] KULLANILMIYOR: RPC'nin byte[] parametresi her alışta yeni dizi ayırıyor
/// (FastBufferReader.cs:857/869) — 30 Hz x 3 konuşmacı = saniyede ~90 çöp dizi. Burada
/// <see cref="FastBufferWriter"/>/<see cref="FastBufferReader"/> + *Safe metodları önceden ayrılmış
/// buffer'lara okuyup/yazıyor → sıcak yolda (tek-fragment, gerçekçi ~%99.9 durum) 0B managed alloc.
/// `Reliable` KULLANILMIYOR: kaybolan bir ses paketinin retransmit'i arkasındaki OYUN DURUMU
/// mesajlarını bekletir (head-of-line blocking) — 300ms gecikmiş ses zaten işe yaramaz.
///
/// İKİ AYRI MESAJ ADI ("CargorVoiceUp" / "CargorVoiceDown"), TEK AD DEĞİL — plandan BİLİNÇLİ SAPMA:
/// Host aynı anda hem server hem client'tır (IsServer VE IsClient true). CustomMessagingManager'ın
/// RegisterNamedMessageHandler'ı isim başına TEK bir delegate tutuyor (düz dictionary ataması,
/// çoklu-yayın DEĞİL — CustomMessageManager.cs:250-251). Tek bir "CargorVoice" adı kullanılıp
/// server-rolü OnServerStarted'da, client-rolü OnClientStarted'da AYRI birer handler kaydetseydi,
/// host'ta İKİNCİ kayıt BİRİNCİYİ sessizce ezerdi (bir rol kırılırdı, hangisi kazanırsa). Ayrıca iki
/// yönün tel formatı da FARKLI (Up: [1B frag][ses], Down: [1B frag][8B relay][ses]) — tek isimle tek
/// handler'dan ikisini de doğru ayırt etmenin güvenilir bir yolu yok. İki ayrı ad, bu iki sorunu da
/// köküyle çözüyor: host'ta iki handler ayrı ayrı yaşar, hiçbiri diğerini ezmez.
///
/// AKIŞ (server-authoritative — CustomMessageManager.cs:357-369 client'ın yalnız server'a
/// gönderebilmesini zorunlu kılıyor):
///   Konuşan client ──"CargorVoiceUp" (4B VoicePacket header + ses, fragment'lanmış)──► Server
///   Server: 8B senderClientId EKLER (TryWriteRelayHeader) ──"CargorVoiceDown"──►
///           ConnectedClientsIds \ {gönderen}  (bkz. RelayFragmentToOthers — TEK filtre, aşağıdaki
///           İKİ senaryoyu da kapatır, iki ayrı kod değil)
/// Gönderen kimliğini CLIENT YAZMAZ — server bunu NGO'nun kendi `senderClientId` parametresinden
/// alır (Up handler). Bu, sahte kimlikle konuşma (spoofing) yolunu kapatıyor.
///
/// 🔴 İKİ AYRI DIŞLAMA (aynı satırda, TEK filtre ile karşılanıyor — bkz. RelayFragmentToOthers):
///   1) Host konuşurken: bu paket AĞ HOP'U OLMADAN doğrudan RelayToOthers'a gelir (senderClientId =
///      host'un kendi LocalClientId'i) → filtre host'u kendi alıcı listesinden çıkarır.
///   2) Uzak bir client konuşurken: paket Up handler'dan (NGO'nun verdiği gerçek sender) gelir →
///      filtre O client'ı kendi alıcı listesinden çıkarır.
/// İkisi de AYNI `id != senderClientId` filtresinden geçer; biri unutulursa (örn. bu filtre atlanıp
/// ConnectedClientsIds ham gönderilirse) NGO'nun SendNamedMessage host-kısayolu (clientIds listesinde
/// LocalClientId varsa handler'ı YERELDE de çağırıyor — CustomMessageManager.cs:397-411) yüzünden
/// host GECİKMELİ kendi sesini duyar. Sessiz, tanısı çok zor bir bug — bu yüzden filtre TEK NOKTADA.
///
/// BOYUT KAPAĞI 800 B (bkz. <see cref="MaxWireMessageBytes"/>): NGO'nun parça-bölünmemiş batch
/// tavanı ~1296 B (NetworkMessageManager.cs:113, DEBUG'da aşılırsa CustomMessageManager.cs:443-449
/// OverflowException atar) ama biz ondan daha muhafazakâr bir sınır seçtik çünkü (a) Steam'in
/// relay'inin tek-datagram eşiği muhtemelen daha alçak (doğrulanmamış varsayım — plan riski #5,
/// yanlışsa sadece gereksiz muhafazakârlık, güvenli yönde hata) ve (b) UNRELIABLE bir mesajda TEK
/// FRAGMENT KAYBI TÜM MESAJI DÜŞÜRÜR — küçük parçalar bu riski küçültür. 800B'yi aşan bir ses örneği
/// (30Hz'de gerçekçi olan ~70-270B'nin ÇOK üstünde, MaxCompressedFrameBytes=4096 savunma tavanının
/// devreye girdiği patolojik durum) <see cref="MaxChunkPayloadBytes"/> boyutunda parçalara bölünür;
/// her parça kendi fragman-index'ini taşır (VoicePacket'in kendi Sequence/Flags alanları burada
/// KULLANILMIYOR — bu, capture'ın 30Hz örnekleme kadansı için ayrı bir kavram, wire-fragmentation
/// kendi 1B başlığını kullanıyor, ikisi birbirine karışmıyor).
///
/// SIRALAMA: FacepunchTransport UnreliableSequenced'i de düz Unreliable'a çeviriyor
/// (FacepunchTransport.cs:110-121) → NGO'nun kendi sıra garantisi yok. Ama bu sınıf wire-fragmanları
/// SIRALI ÜRETİYOR ve alıcı tarafta sıkı bir "beklenen index" kontrolüyle doğruluyor (bkz.
/// OnDownMessageReceived) — sıra dışı/kayıp bir fragman gelirse o GRUBUN TAMAMI atılır (kısmi
/// decode denenmiyor, Steam'in DecompressVoice'u yarım bir sıkıştırılmış frame'den zaten anlamlı
/// ses üretemez). 30Hz kadansındaki ASIL ses-örneği sıralaması (dup/reorder/gap) bu sınıfın işi
/// DEĞİL — reassemble edilen tam paket <see cref="RadioVoicePlayback.HandleIncomingPacket"/>'e
/// verildiğinde, o VoicePacket başlığını okuyup VoiceSequenceTracker'a (Core, zaten var) sokuyor.
///
/// TEK OYUNCU / AĞ YOK: SendCapturedPacket ağ yokken veya ConnectedClientsIds.Count &lt;= 1 iken
/// SESSİZCE düşürür — yakalama (HUD/Kendini Dinle/Steam iç tampon boşaltma) RadioVoiceRuntime'da
/// AYRICA ve HER ZAMAN yapılıyor, bu sınıf sadece "ağa çık" bacağı.
/// </summary>
public sealed class RadioVoiceTransport
{
    private const string UpMessageName = "CargorVoiceUp";
    private const string DownMessageName = "CargorVoiceDown";

    private const int MaxWireMessageBytes = 800;
    private const int FragmentHeaderSize = 1; // bit7 = IsLast, bit0-6 = fragment index (0-126)

    /// <summary>
    /// Bir tek ağ mesajına sığacak ham ses-parçası boyutu. 800'den 1B (kendi fragman başlığımız)
    /// VE 8B (relay hop'unun ekleyeceği senderClientId) düşülüyor — AYNI sabit HER İKİ hop'ta da
    /// kullanılıyor ki client→server hop'unda fragmanlanan bir parça, server→client hop'unda relay
    /// başlığı eklenince kapağı AŞMASIN (client hop'unda 8B'lik pay kullanılmıyor ama rezerve
    /// edilmiş kalıyor — küçük bir israf, ama tek sabitle iki hop'u da güvenceye alan basit çözüm).
    /// </summary>
    private const int MaxChunkPayloadBytes = MaxWireMessageBytes - FragmentHeaderSize - VoicePacket.RelayHeaderSize; // 791

    private readonly RadioVoicePlayback _playback;

    private bool _upRegistered;
    private bool _downRegistered;

    // Sıcak yol (tek-fragman, gerçekçi durumların ezici çoğunluğu) — TEK paylaşılan scratch: NGO'nun
    // mesaj dispatch'i ana thread'de SIRALI (concurrent değil), bu yüzden ardışık çağrılar arasında
    // yeniden kullanmak güvenli. Boyut = VoicePacket başlığı + capture'ın üretebileceği en büyük ham frame.
    // DİKKAT — bu üç alan bilinçli olarak `readonly` DEĞİL, ve öyle kalmalı.
    // FastBufferReader.ReadBytesSafe(ref byte[] ...) imzası `ref` istiyor (içinde `fixed` bloğu
    // kuruyor), C# ise `readonly` bir alanı `ref` olarak geçirmeye izin vermiyor (CS0192).
    // `ref` burada "yeniden ayırabilir" anlamına GELMİYOR — diziler init'te bir kez ayrılıyor ve
    // hiçbir yerde yeniden atanmıyor, yani "runtime'da 0 alloc" kuralı korunuyor.
    // `readonly` eklemek derlemeyi kırar; eklemeyin.
    private byte[] _downFastScratch = new byte[VoicePacket.HeaderSize + RadioVoiceCapture.MaxCompressedFrameBytes];
    private byte[] _serverRelayScratch = new byte[MaxChunkPayloadBytes];
    private byte[] _relayHeaderScratch = new byte[VoicePacket.RelayHeaderSize];
    private readonly List<ulong> _recipientScratch = new List<ulong>(4); // proje 1-4 oyuncu — yeniden kullanılan liste, per-relay alloc yok

    // Yavaş/nadir yol: >791B'lik TEK ses örneği çoklu ağ mesajına bölündüğünde konuşmacı başına
    // reassembly durumu. Lazy — sadece bir speaker GERÇEKTEN parçalanmış bir paket gönderdiğinde
    // (patolojik/nadir) ayrılır, 30Hz sıcak yolu hiç etkilemez (kabul kriteri: "0B/frame" sıcak yol içindir).
    private readonly Dictionary<ulong, byte[]> _reassemblyBuffers = new Dictionary<ulong, byte[]>();
    private readonly Dictionary<ulong, int> _reassemblyWrittenBytes = new Dictionary<ulong, int>();
    private readonly Dictionary<ulong, int> _reassemblyExpectedIndex = new Dictionary<ulong, int>();

    private bool _warnedRecipientsEmptyThisSession;

    public RadioVoiceTransport(RadioVoicePlayback playback)
    {
        _playback = playback ?? throw new ArgumentNullException(nameof(playback));
    }

    // ---------------------------------------------------------------------------------------------
    // Yaşam döngüsü kancaları — RadioVoiceRuntime, NetworkManager.Singleton'ın Started/Stopped
    // event'lerini kendi Update()'inde tespit edip buraya yönlendiriyor (CustomMessagingManager
    // NetworkManager başlamadan NULL — Awake'te değil, bu yüzden bu metodlar ayrı).
    // ---------------------------------------------------------------------------------------------

    /// <summary>Server (host dahil) ağ dinlemeye başladığında: client'lardan gelen ham ses
    /// fragmanlarını relay etmek için "CargorVoiceUp" handler'ını kaydeder.</summary>
    public void OnNetworkServerStarted()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || nm.CustomMessagingManager == null) return; // savunma — normalde burada null olmamalı
        if (_upRegistered) return;

        nm.CustomMessagingManager.RegisterNamedMessageHandler(UpMessageName, OnUpMessageReceived);
        _upRegistered = true;
    }

    public void OnNetworkServerStopped()
    {
        var nm = NetworkManager.Singleton;
        if (_upRegistered)
        {
            nm?.CustomMessagingManager?.UnregisterNamedMessageHandler(UpMessageName);
            _upRegistered = false;
        }
    }

    /// <summary>Herhangi bir client (host dahil) ağa bağlandığında: server'ın relay ettiği sesi
    /// çözmek için "CargorVoiceDown" handler'ını + disconnect-bazlı slot serbest bırakmayı kaydeder.</summary>
    public void OnNetworkClientStarted()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || nm.CustomMessagingManager == null) return;
        if (_downRegistered) return;

        nm.CustomMessagingManager.RegisterNamedMessageHandler(DownMessageName, OnDownMessageReceived);
        nm.OnClientDisconnectCallback += OnClientDisconnected; // plan: "konuşan disconnect → slot serbest"
        _downRegistered = true;
    }

    public void OnNetworkClientStopped()
    {
        var nm = NetworkManager.Singleton;
        if (_downRegistered)
        {
            if (nm != null)
            {
                nm.CustomMessagingManager?.UnregisterNamedMessageHandler(DownMessageName);
                nm.OnClientDisconnectCallback -= OnClientDisconnected;
            }
            _downRegistered = false;
        }
        ClearReassemblyState();
    }

    /// <summary>RadioVoiceRuntime.TeardownVoice() çağırır (mikrofon+her yerden). Ağ handler kaydına
    /// DOKUNMAZ — o kayıt SADECE gerçek OnNetworkClientStopped/OnNetworkServerStopped ile 1:1
    /// eşleşir (sahne değişimi/OnDisable gibi diğer teardown tetikleyicileri sırasında bağlantı canlı
    /// kalabilir; handler'ı orada da söküp OnClientStarted bir daha ASLA tetiklenmezse — bağlantı
    /// sürerken sahne değişimi olduğunda — telsiz kalıcı olarak sessiz kalırdı). Burada sadece
    /// yarım kalmış reassembly durumu (bellek hijyeni, ağdan bağımsız güvenli) temizlenir.</summary>
    public void ClearReassemblyState()
    {
        _reassemblyBuffers.Clear();
        _reassemblyWrittenBytes.Clear();
        _reassemblyExpectedIndex.Clear();
    }

    // ---------------------------------------------------------------------------------------------
    // Gönderme (yerel mikrofon → ağ)
    // ---------------------------------------------------------------------------------------------

    /// <summary>RadioVoiceRuntime.OnCapturedPacket buraya YAKALANAN HER paketi verir (SelfMonitor
    /// loopback'e EK — yerine geçmiyor). Tek oyuncu/ağ-yok/CustomMessagingManager henüz hazır değil
    /// durumlarında sessizce düşer; yakalamanın kendisi zaten Runtime'da ağdan bağımsız çalışıyor.</summary>
    public void SendCapturedPacket(ArraySegment<byte> fullPacket)
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsListening) return; // sahne-içi ama ağ yok (örn. tek oyuncu offline) — düş
        if (nm.ConnectedClientsIds.Count <= 1) return; // plan §Tek oyuncu: yakalama YAPILIR, paket ağa ÇIKMAZ
        if (nm.CustomMessagingManager == null) return; // savunma — yıkım sırası garanti değil

        if (nm.IsServer)
        {
            // Host konuşuyor: kendi kendine "server'a gönder" turu yapmadan DOĞRUDAN relay adımına
            // gir. senderClientId = host'un kendi LocalClientId'i — İKİ DIŞLAMANIN 1. maddesi burada
            // RelayFragmentToOthers'ın TEK filtresiyle karşılanıyor (host kendi listesinden düşer).
            RelayToOthers(nm.LocalClientId, fullPacket);
        }
        else
        {
            SendFragmentsUp(fullPacket);
        }
    }

    private void SendFragmentsUp(ArraySegment<byte> fullPacket)
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || nm.CustomMessagingManager == null) return;

        int total = fullPacket.Count;
        int fragCount = ComputeFragmentCount(total);

        for (int i = 0; i < fragCount; i++)
        {
            ComputeFragment(i, total, out int chunkOffset, out int chunkLen, out bool isLast);
            byte fragHeader = EncodeFragHeader(i, isLast);

            var writer = new FastBufferWriter(FragmentHeaderSize + chunkLen, Allocator.Temp);
            try
            {
                writer.WriteByteSafe(fragHeader);
                writer.WriteBytesSafe(fullPacket.Array, chunkLen, fullPacket.Offset + chunkOffset);
                // Client yalnız server'a gönderebilir (CustomMessageManager.cs:357-369) — tekil
                // clientId overload'u bu kısıtlamayı zorunlu kılmıyor bile, hedef doğrudan server.
                nm.CustomMessagingManager.SendNamedMessage(UpMessageName, NetworkManager.ServerClientId, writer, NetworkDelivery.Unreliable);
            }
            finally
            {
                writer.Dispose();
            }
        }
    }

    /// <summary>Server (host dahil) tarafında: bir "tam paket"i (host'un kendi sesi VEYA Up'tan
    /// gelmiş — ama Up zaten fragmanlanmış geldiği için BU metod SADECE host'un kendi doğrudan
    /// yolunda çağrılır, bkz. SendCapturedPacket) parçalara böler ve her parçayı relay eder.</summary>
    private void RelayToOthers(ulong senderClientId, ArraySegment<byte> fullPacket)
    {
        int total = fullPacket.Count;
        int fragCount = ComputeFragmentCount(total);

        for (int i = 0; i < fragCount; i++)
        {
            ComputeFragment(i, total, out int chunkOffset, out int chunkLen, out bool isLast);
            byte fragHeader = EncodeFragHeader(i, isLast);

            RelayFragmentToOthers(senderClientId, fragHeader,
                new ArraySegment<byte>(fullPacket.Array, fullPacket.Offset + chunkOffset, chunkLen));
        }
    }

    /// <summary>TEK relay noktası — hem host'un kendi sesi (SendCapturedPacket→RelayToOthers) HEM
    /// uzak bir client'ın sesi (OnUpMessageReceived) BURAYA gelir. İKİ AYRI DIŞLAMA (sınıf yorumuna
    /// bak) burada AYNI tek satırla karşılanıyor: `id != senderClientId`. Biri host, biri uzak client
    /// olsa da filtre aynı — senderClientId doğru geçirildiği sürece ikisi de doğru çalışır.</summary>
    private void RelayFragmentToOthers(ulong senderClientId, byte fragHeader, ArraySegment<byte> chunk)
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsListening || nm.CustomMessagingManager == null) return;

        _recipientScratch.Clear();
        var connected = nm.ConnectedClientsIds;
        for (int i = 0; i < connected.Count; i++)
        {
            if (connected[i] != senderClientId) _recipientScratch.Add(connected[i]);
        }

        if (_recipientScratch.Count == 0)
        {
            // Boş listeyle SendNamedMessage hata loglar (CustomMessageManager.cs:379-383) — tek
            // dinleyicili bir oturumda (gönderenden başka kimse yok) bu NORMAL bir durum, spam yasak.
            if (!_warnedRecipientsEmptyThisSession)
            {
                Debug.Log("[RadioVoiceTransport] Relay için alıcı yok (gönderenden başka bağlı kimse yok) — paket düşürüldü. (oturum başına bir kez loglanır)");
                _warnedRecipientsEmptyThisSession = true;
            }
            return;
        }

        int totalSize = FragmentHeaderSize + VoicePacket.RelayHeaderSize + chunk.Count;
        var writer = new FastBufferWriter(totalSize, Allocator.Temp);
        try
        {
            writer.WriteByteSafe(fragHeader);
            WriteRelayHeader(ref writer, senderClientId);
            writer.WriteBytesSafe(chunk.Array, chunk.Count, chunk.Offset);
            nm.CustomMessagingManager.SendNamedMessage(DownMessageName, _recipientScratch, writer, NetworkDelivery.Unreliable);
        }
        finally
        {
            writer.Dispose();
        }
    }

    private void WriteRelayHeader(ref FastBufferWriter writer, ulong senderClientId)
    {
        // VoicePacket'in HAZIR relay-header yardımcısı kullanılıyor (Core'a hiç dokunmadan) — sabit
        // 8B'lik scratch'e yazıp oradan writer'a kopyalanıyor (FastBufferWriter'ın byte[] overload'u
        // zaten pointer-fix üzerinden kopyalıyor, burada ekstra bir alloc yok).
        VoicePacket.TryWriteRelayHeader(_relayHeaderScratch, 0, _relayHeaderScratch.Length, senderClientId);
        writer.WriteBytesSafe(_relayHeaderScratch, VoicePacket.RelayHeaderSize, 0);
    }

    // ---------------------------------------------------------------------------------------------
    // Alma — Server rolü: client'lardan ham ses fragmanı gelir, relay'e devredilir (reassembly YOK,
    // server her fragmanı 1:1 aynı boyutta ileri taşır — kendi başına anlamlı bir "tam paket" değil).
    // ---------------------------------------------------------------------------------------------

    private void OnUpMessageReceived(ulong senderClientId, FastBufferReader reader)
    {
        if (reader.Length - reader.Position < FragmentHeaderSize) return; // bozuk/kısa mesaj — sessizce düş

        reader.ReadByteSafe(out byte fragHeader);
        int chunkLen = reader.Length - reader.Position;
        if (chunkLen <= 0 || chunkLen > _serverRelayScratch.Length) return; // savunma: pathological/bozuk boy

        reader.ReadBytesSafe(ref _serverRelayScratch, chunkLen);
        RelayFragmentToOthers(senderClientId, fragHeader, new ArraySegment<byte>(_serverRelayScratch, 0, chunkLen));
    }

    // ---------------------------------------------------------------------------------------------
    // Alma — Client rolü: server'dan relay edilmiş ses gelir (relay header İÇERİR). Sıcak yol
    // (tek fragman) reassembly'e hiç dokunmadan direkt Playback'e gider; nadir çok-fragmanlı yol
    // konuşmacı başına küçük bir tampon kullanır.
    // ---------------------------------------------------------------------------------------------

    private void OnDownMessageReceived(ulong _ /* her zaman ServerClientId — orijinal konuşmacı relay header'dan okunur */, FastBufferReader reader)
    {
        if (reader.Length - reader.Position < FragmentHeaderSize + VoicePacket.RelayHeaderSize) return; // bozuk/kısa — düş

        reader.ReadByteSafe(out byte fragHeader);
        reader.ReadBytesSafe(ref _relayHeaderScratch, VoicePacket.RelayHeaderSize);
        if (!VoicePacket.TryReadRelayHeader(_relayHeaderScratch, 0, VoicePacket.RelayHeaderSize, out ulong speakerId)) return;

        var nm = NetworkManager.Singleton;
        if (nm != null && speakerId == nm.LocalClientId) return; // ek sigorta — İKİ DIŞLAMA doğru çalışıyorsa buraya asla düşülmemeli

        int chunkLen = reader.Length - reader.Position;
        if (chunkLen < 0) return;

        DecodeFragHeader(fragHeader, out int fragIndex, out bool isLast);

        if (fragIndex == 0 && isLast)
        {
            // SICAK YOL: parçalanmamış tek-mesaj paket — 30Hz'de gerçekçi ~%99.9 durum (plan: ~70-270B,
            // 791B kapağının çok altında). Reassembly dictionary'sine HİÇ dokunulmuyor, 0B/frame kuralı korunuyor.
            if (chunkLen > _downFastScratch.Length) return; // savunma
            reader.ReadBytesSafe(ref _downFastScratch, chunkLen);
            _playback.HandleIncomingPacket(speakerId, new ArraySegment<byte>(_downFastScratch, 0, chunkLen));
            return;
        }

        HandleFragmentedDownMessage(speakerId, fragIndex, isLast, chunkLen, ref reader);
    }

    /// <summary>NADİR yol: bir ses örneği 800B kapağını aştığı için birden çok ağ mesajına bölünmüş.
    /// Konuşmacı başına lazy (sadece bu duruma İLK düşüldüğünde ayrılan) reassembly tamponu kullanır —
    /// 30Hz sıcak yolu hiç etkilemez. Sıra dışı/kayıp bir fragman gelirse grup TAMAMEN atılır: unreliable
    /// bir mesajda tek fragman kaybı zaten tüm mesajı düşürür (plan §2, bilinçli kabul) — kısmi/bozuk
    /// bir sıkıştırılmış frame'i Steam'in DecompressVoice'una vermek anlamlı ses üretmez.</summary>
    private void HandleFragmentedDownMessage(ulong speakerId, int fragIndex, bool isLast, int chunkLen, ref FastBufferReader reader)
    {
        if (!_reassemblyBuffers.TryGetValue(speakerId, out var buf))
        {
            buf = new byte[VoicePacket.HeaderSize + RadioVoiceCapture.MaxCompressedFrameBytes]; // lazy, konuşmacı ömrü boyunca bir kez
            _reassemblyBuffers[speakerId] = buf;
        }

        int expectedIndex = _reassemblyExpectedIndex.TryGetValue(speakerId, out var e) ? e : 0;
        int chunkOffset = fragIndex * MaxChunkPayloadBytes;

        if (fragIndex != expectedIndex || chunkOffset + chunkLen > buf.Length)
        {
            // Sırası bozuk/kayıp/pathological boy — bu grubu kurtarma girişimi YOK, sıfırdan bekle.
            _reassemblyExpectedIndex[speakerId] = 0;
            _reassemblyWrittenBytes[speakerId] = 0;
            return;
        }

        reader.ReadBytesSafe(ref buf, chunkLen, chunkOffset);
        int writtenSoFar = chunkOffset + chunkLen;
        _reassemblyWrittenBytes[speakerId] = writtenSoFar;

        if (isLast)
        {
            _reassemblyExpectedIndex[speakerId] = 0;
            _playback.HandleIncomingPacket(speakerId, new ArraySegment<byte>(buf, 0, writtenSoFar));
        }
        else
        {
            _reassemblyExpectedIndex[speakerId] = fragIndex + 1;
        }
    }

    private void OnClientDisconnected(ulong clientId)
    {
        // Plan: "konuşan disconnect → slot serbest + ~15ms fade-out". ForceReleaseSlot zaten var
        // (RadioVoicePlayback, Dalga 2) ve NotifySpeakerEnded ring buffer'a KASITLI dokunmuyor —
        // Dalga 2'nin kendi yorumuna göre (RadioVoiceSpeakerSlot.NotifySpeakerEnded) akış kendi
        // underrun fade'iyle (~2ms kosinüs, VoiceRingBuffer/VoiceBufferPolicy — Core) doğal olarak
        // sessizliğe dönüyor. Ayrı bir 15ms fade eklemek Core'u (VoiceRingBuffer sabitleri + EditMode
        // testleri) değiştirmek demek — "gerekçesiz Core değişikliği yasak" kuralına göre bu dalganın
        // kapsamı DIŞINDA tutuldu; rapora not düşüldü (bkz. RadioVoiceTransport raporu).
        _playback.ForceReleaseSlot(clientId);

        // Disconnect ortasında yarım kalmış bir reassembly grubu varsa at — aynı clientId nadiren
        // yeniden kullanılırsa (yeni bağlantı) eski parçalarla karışmasın.
        _reassemblyBuffers.Remove(clientId);
        _reassemblyWrittenBytes.Remove(clientId);
        _reassemblyExpectedIndex.Remove(clientId);
    }

    // ---------------------------------------------------------------------------------------------
    // Fragman matematiği — pure, closure/alloc YOK (static local hesap, delegate/lambda kullanılmıyor
    // ki 30Hz sıcak yolda per-call bir closure/delegate alloc'u sızmasın).
    // ---------------------------------------------------------------------------------------------

    private static int ComputeFragmentCount(int totalBytes)
    {
        if (totalBytes <= 0) return 0;
        return (totalBytes + MaxChunkPayloadBytes - 1) / MaxChunkPayloadBytes;
    }

    private static void ComputeFragment(int index, int totalBytes, out int offset, out int length, out bool isLast)
    {
        offset = index * MaxChunkPayloadBytes;
        int remaining = totalBytes - offset;
        length = Math.Min(MaxChunkPayloadBytes, remaining);
        isLast = (offset + length) >= totalBytes;
    }

    private static byte EncodeFragHeader(int index, bool isLast)
    {
        return (byte)((isLast ? 0x80 : 0) | (index & 0x7F));
    }

    private static void DecodeFragHeader(byte header, out int index, out bool isLast)
    {
        index = header & 0x7F;
        isLast = (header & 0x80) != 0;
    }
}
