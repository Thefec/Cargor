using System;
using System.Threading;

namespace NewCss.Voice.Core
{
    /// <summary>
    /// Tek-üretici / tek-tüketici (SPSC) KİLİTSİZ float ring buffer. Ana thread (ağdan gelen
    /// paketleri çözüp örnek yazan) üretici, audio thread (`PCMReaderCallback`) tüketicidir.
    ///
    /// LOCK YASAK: audio thread bir `lock` alırken ana thread tam o anda bir GC pause'a girerse
    /// audio thread bloke olur ve bu doğrudan duyulabilir bir dropout'a (cızırtı/donma) dönüşür.
    /// Bu yüzden bu sınıfta `lock`, `new`/allocation (kurulum dışında), LINQ veya herhangi bir
    /// Unity API YOKTUR. Tüm senkronizasyon iki `int` indeks + `Volatile.Read`/`Volatile.Write`
    /// yayınlama bariyeriyle yapılır: üretici veriyi diziye YAZDIKTAN SONRA `_write`'ı yayınlar,
    /// tüketici `_write`'ı OKUDUKTAN SONRA diziden veri okur — bu sıra veri görünürlüğünün
    /// garantisidir (bir thread'in yazdığı veri, indeks güncellemesi görülmeden asla okunmaz).
    /// Sırayı bozmak (örn. yayınlamayı veriden önce yapmak) yarış durumu ve çöp/eski örnek okumaya yol açar.
    ///
    /// Kapasite ve tüm eşikler ÖRNEK SAYISI olarak parametre gelir — 48000 gibi bir sabit
    /// hardcode edilmez (bkz. VoiceBufferPolicy: `AudioManager.asset`'te `m_SampleRate: 0`,
    /// gerçek hız donanıma göre değişir).
    /// </summary>
    public sealed class VoiceRingBuffer
    {
        private readonly float[] _buffer;
        private readonly int _capacity;
        private readonly int _targetDelaySamples;
        private readonly int _overrunCeilingSamples;
        private readonly int _fadeSamples;

        // SADECE üretici (ana thread) yazar — overrun düzeltmesi hariç, bkz. Write() içindeki not.
        private int _write;

        // SADECE tüketici (audio thread) yazar — TEK istisna: üretici, overrun'da doluluğu tavana
        // çarptığında en eski veriyi atmak için bu indeksi de ileri sarar. Bu, formel anlamda
        // "tek yazar" kuralını gevşetir ama kasıtlıdır: overrun nadir bir olay ve düzeltmeyi kimin
        // yaptığı önemli değil, önemli olan indeksin HER ZAMAN Volatile.Write ile yayınlanmasıdır.
        private int _read;

        // Tüketici-yerel durum: kilit gerektirmez, sadece audio thread okur/yazar.
        private readonly int _resumeDelaySamples;

        private bool _gateOpen;
        private bool _pendingFadeIn;

        /// <summary>
        /// TÜKETİCİ-YEREL (yalnız audio thread okur/yazar, kilit gerekmez): burst ortasında bir
        /// underrun yaşandı mı? true ise kapı <see cref="_resumeDelaySamples"/> ile açılır,
        /// false ise <see cref="_targetDelaySamples"/> ile. Bkz. VoiceBufferPolicy.ResumeDelayMs.
        /// </summary>
        private bool _resumeMode;

        /// <summary>
        /// ANA THREAD → audio thread tek yönlü bayrak: konuşmacının burst'ü bitti, sonraki playout
        /// yeniden TAM hedefi beklesin. `Volatile` ile yayınlanıyor; tüketici Read()'in başında
        /// tüketip sıfırlıyor — böylece <see cref="_resumeMode"/> tüketici-yerel kalıyor ve
        /// audio thread'e hiçbir kilit girmiyor.
        /// </summary>
        private int _burstEndFlag;

        private int _underrunCount;
        private int _overrunCount;
        private int _droppedSamples;

        /// <param name="capacitySamples">Fiziksel dizi boyutu. Bir kez ayrılır (kurulumda), runtime'da asla büyümez.</param>
        /// <param name="targetDelaySamples">Playout kapısının açılması için gereken minimum doluluk.</param>
        /// <param name="overrunCeilingSamples">Bu doluluğun üstünde en eski örnekler atılır; capacitySamples'ı aşamaz.</param>
        /// <param name="fadeSamples">Underrun/overrun geçişlerinde uygulanan kosinüs fade'in örnek sayısı (~2 ms).</param>
        /// <param name="resumeDelaySamples">
        /// Burst ORTASINDA underrun sonrası yeniden açılma eşiği (asimetrik kapı — bkz.
        /// VoiceBufferPolicy.ResumeDelayMs). -1 (varsayılan) = <paramref name="targetDelaySamples"/>
        /// ile aynı, yani eski simetrik davranış.
        /// </param>
        public VoiceRingBuffer(int capacitySamples, int targetDelaySamples, int overrunCeilingSamples, int fadeSamples,
                               int resumeDelaySamples = -1)
        {
            if (capacitySamples <= 0) throw new ArgumentOutOfRangeException(nameof(capacitySamples));
            if (targetDelaySamples < 0) throw new ArgumentOutOfRangeException(nameof(targetDelaySamples));
            if (fadeSamples < 0) throw new ArgumentOutOfRangeException(nameof(fadeSamples));

            _capacity = capacitySamples;
            _targetDelaySamples = Math.Min(targetDelaySamples, capacitySamples);
            _resumeDelaySamples = resumeDelaySamples < 0
                ? _targetDelaySamples
                : Math.Min(resumeDelaySamples, _targetDelaySamples);
            _overrunCeilingSamples = Math.Min(Math.Max(overrunCeilingSamples, _targetDelaySamples), capacitySamples);
            _fadeSamples = fadeSamples;

            // TEK ayırma — kurulumda, hot path'te (Read/Write) değil.
            _buffer = new float[capacitySamples];
        }

        public int Capacity => _capacity;
        public int TargetDelaySamples => _targetDelaySamples;
        public int OverrunCeilingSamples => _overrunCeilingSamples;
        public int FadeSamples => _fadeSamples;

        /// <summary>Playout kapısı şu anda açık mı (yani hedef gecikme bir kez dolduruldu mu).</summary>
        public bool IsGateOpen => _gateOpen;

        /// <summary>Şu an buffer'da bekleyen (henüz okunmamış) örnek sayısı.</summary>
        public int AvailableSamples => Volatile.Read(ref _write) - Volatile.Read(ref _read);

        /// <summary>
        /// ANA THREAD'den çağrılır (konuşmacının burst'ü bitti — BurstEnd bayrağı veya zaman aşımı).
        /// Asimetrik kapıyı sıfırlar: sonraki konuşma yeniden TAM hedef gecikmeyi bekler, çünkü yeni
        /// bir burst'ün başında gerçek ağ jitter'ına karşı dolu tampon gerekiyor.
        /// Kilit YOK — tek yönlü Volatile bayrak, tüketici Read()'te tüketiyor.
        /// </summary>
        public void NotifyBurstEnded() => Volatile.Write(ref _burstEndFlag, 1);

        /// <summary>
        /// ÜRETİCİ (ana thread) tarafı drift düzeltmesi: gecikmeyi AZALTMAK için en eski
        /// <paramref name="samples"/> örneği atar. Overrun yolundaki mekanizmanın aynısı, ama tavan
        /// aşılmadan, yavaş sürüklenmeyi toparlamak için (bkz. VoiceDriftTracker).
        /// Mevcut veriden fazlasını atmaz; kapıyı kapatmaz.
        /// </summary>
        /// <returns>Fiilen atılan örnek sayısı.</returns>
        public int DriftDropOldest(int samples)
        {
            if (samples <= 0) return 0;

            int readSnapshot = Volatile.Read(ref _read);
            int available = Volatile.Read(ref _write) - readSnapshot;
            if (available <= 0) return 0;

            // Tamamen boşaltmayız: en az bir "devam eşiği" kadar bırak, yoksa düzeltmenin kendisi
            // underrun tetikler (tedavi hastalıktan kötü olur).
            int droppable = Math.Max(0, available - _resumeDelaySamples);
            int toDrop = Math.Min(samples, droppable);
            if (toDrop <= 0) return 0;

            Volatile.Write(ref _read, readSnapshot + toDrop);
            Interlocked.Add(ref _droppedSamples, toDrop);
            return toDrop;
        }

        /// <summary>
        /// ÜRETİCİ (ana thread) tarafı drift düzeltmesi: gecikmeyi ARTIRMAK için
        /// <paramref name="samples"/> kadar sessizlik yazar. Kaynak dizi gerektirmez (çağrı yerinde
        /// sıfır allocation). Tavanı aşmaz.
        /// </summary>
        /// <returns>Fiilen yazılan örnek sayısı.</returns>
        public int DriftInsertSilence(int samples)
        {
            if (samples <= 0) return 0;

            int writeLocal = _write;
            int available = writeLocal - Volatile.Read(ref _read);
            int room = Math.Max(0, _overrunCeilingSamples - available);
            int toWrite = Math.Min(samples, room);
            if (toWrite <= 0) return 0;

            for (int i = 0; i < toWrite; i++)
                _buffer[Wrap(writeLocal + i)] = 0f;

            Volatile.Write(ref _write, writeLocal + toWrite); // veri YAZILDIKTAN SONRA yayınla
            return toWrite;
        }

        /// <summary>Teşhis/test: kapı şu an küçük "devam" eşiğini mi kullanıyor?</summary>
        public bool IsInResumeMode => _resumeMode;

        public int UnderrunCount => Volatile.Read(ref _underrunCount);
        public int OverrunCount => Volatile.Read(ref _overrunCount);
        public int DroppedSamples => Volatile.Read(ref _droppedSamples);

        /// <summary>
        /// ÜRETİCİ tarafı: ağdan çözülen örnekleri buffer'a yazar. Ana thread'den çağrılır.
        /// </summary>
        public void Write(float[] source, int sourceOffset, int count)
        {
            if (count <= 0) return;

            int writeLocal = _write; // üretici kendi son yazdığı noktayı normal okur, cross-thread değil
            int readSnapshot = Volatile.Read(ref _read); // tüketicinin en son ilerlettiği nokta (taze görüş)
            int used = writeLocal - readSnapshot;

            if (used + count > _overrunCeilingSamples)
            {
                // OVERRUN: doluluk tavanı aşılıyor. EN ESKİYİ at (en yeniyi atmak gecikmeyi
                // sonsuza büyütür — klasik hata). _read'i ileri sarıp gecikmeyi TAM hedefe resetle.
                int oldRead = readSnapshot;
                int newRead = writeLocal + count - _targetDelaySamples;
                if (newRead < oldRead) newRead = oldRead; // güvenlik: asla geriye gitme (used negatif olamaz)

                int dropped = newRead - oldRead;
                if (dropped > 0)
                {
                    Interlocked.Add(ref _droppedSamples, dropped);

                    // Zıplama noktasında ~2 ms crossfade: dinleyicinin bir sonraki okumada duyacağı
                    // "kalan" bölgeyi (newRead'den itibaren), az önce terk edilen "atlanan" bölgenin
                    // (oldRead'den itibaren) sönümlenen bir yankısıyla harmanlar. Amaç ani sıçramayı
                    // (duyulan click) yumuşatmak — pitch/gerdirme YAPILMAZ, sadece kısa bir harman.
                    int fade = Math.Min(_fadeSamples, dropped);
                    if (fade > 0)
                        ApplyJumpCrossfade(oldRead, newRead, fade);
                }

                Interlocked.Increment(ref _overrunCount);

                // BİLİNÇLİ SIRA — sonucu güvenli tarafta, dokunmayın.
                // `_read` burada, yeni örnekler `_buffer`'a yazılmadan ve `_write` ilerlemeden ÖNCE
                // yayınlanıyor. Yani aşağıdaki döngü boyunca tüketici (audio thread) `_read`'i yeni,
                // `_write`'ı hâlâ eski görebilir → `AvailableSamples` bir an için küçülür/negatife
                // düşer. Bunun tek sonucu O callback'te fazladan sessizlik dönmesidir: `Read`
                // yetersiz veride ASLA kısa dönmez, sıfırla doldurur ve playout kapısını yeniden
                // kurar. Veri bozulması veya çökme YOK.
                // Sırayı ters çevirmek (önce veri+`_write`, sonra `_read`) bu pencereyi kapatmıyor,
                // yerine BAŞKA bir pencere açıyor: tüketici o anda hâlâ [oldRead, newRead) bölgesini
                // okuyor olabilir ve biz tam oraya yeni örnek yazarız → yarı-üzerine-yazılmış bölge
                // duyulur. İkisi de yalnızca overrun anında (ağır jitter fırtınası) mümkün; fazladan
                // sessizlik, bozuk örnekten iyidir. Gerçek çözüm iki indeksi tek atomik adımda
                // yayınlamak olurdu (tek `long`'a paketlemek) — bu dosyanın kapsamı dışı.
                Volatile.Write(ref _read, newRead);
            }

            for (int i = 0; i < count; i++)
                _buffer[Wrap(writeLocal + i)] = source[sourceOffset + i];

            writeLocal += count;
            Volatile.Write(ref _write, writeLocal); // yayınlama bariyeri: veri YAZILDIKTAN SONRA işaretle
        }

        /// <summary>
        /// TÜKETİCİ tarafı: `PCMReaderCallback` içinden çağrılır. `destination`'ı tam `count`
        /// örnekle doldurur (yetersiz veri varsa sıfırla doldurur, ASLA kısa dönmez — Unity'nin
        /// audio callback sözleşmesi buffer'ın tam doldurulmasını bekler).
        /// Dönüş değeri: gerçek ses üretildiyse true, sessizlik (kapı kapalı ya da underrun) ise false.
        /// </summary>
        public bool Read(float[] destination, int destOffset, int count)
        {
            if (count <= 0) return true;

            // Burst bitti bildirimi geldiyse tüket: sonraki playout yeniden TAM hedefi bekleyecek.
            if (Volatile.Read(ref _burstEndFlag) != 0)
            {
                Volatile.Write(ref _burstEndFlag, 0);
                _resumeMode = false;
            }

            int readLocal = Volatile.Read(ref _read); // üretici overrun'da bunu ilerletmiş olabilir, taze oku
            int writeSnapshot = Volatile.Read(ref _write);
            int available = writeSnapshot - readLocal;

            if (!_gateOpen)
            {
                // ASİMETRİK KAPI: burst başında tam hedef, burst ortasındaki duraklamadan sonra
                // küçük "devam" eşiği. Gerekçe VoiceBufferPolicy.ResumeDelayMs'te.
                int requiredToOpen = _resumeMode ? _resumeDelaySamples : _targetDelaySamples;
                if (available >= requiredToOpen)
                {
                    // PLAYOUT KAPISI: hedef gecikme dolmadan çalmaya BAŞLANMAZ. Cızırtı/robot ses
                    // şikayetlerinin çoğu bu kapı yokken ilk paketin hemen çalınıp arkasının
                    // yetişememesinden doğar.
                    _gateOpen = true;
                    _pendingFadeIn = true;
                }
                else
                {
                    Array.Clear(destination, destOffset, count);
                    return false;
                }
            }

            if (available < count)
            {
                // UNDERRUN: sıfır yaz, _read'i _write'ın ÖTESİNE ASLA geçirme (bu yüzden haveSamples
                // = available, count değil). Girişte/çıkışta ~2 ms kosinüs fade — ani sıfıra atlama
                // duyulan bir "click" üretir, bu fade onu önler. Pitch/gerdirme YAPILMAZ.
                int haveSamples = Math.Max(0, available);
                CopyWithFadeOut(destination, destOffset, readLocal, haveSamples);
                if (count > haveSamples)
                    Array.Clear(destination, destOffset + haveSamples, count - haveSamples);

                readLocal += haveSamples;
                Volatile.Write(ref _read, readLocal);

                Interlocked.Increment(ref _underrunCount);
                _gateOpen = false; // kapı yeniden kurulur: eşik dolana kadar bir sonraki Read sessiz kalır
                _pendingFadeIn = false;
                _resumeMode = true; // ...ama artık TAM hedef değil, küçük "devam" eşiği yeterli (asimetrik kapı)
                return false;
            }

            CopySamples(destination, destOffset, readLocal, count);
            if (_pendingFadeIn)
            {
                // Kapı bu çağrıda ilk kez açıldı: ilk fadeSamples örneği 0'dan 1'e kosinüs eğrisiyle
                // getir, aksi hâlde playout başlangıcında da bir click oluşur.
                ApplyFadeIn(destination, destOffset, Math.Min(_fadeSamples, count));
                _pendingFadeIn = false;
            }

            readLocal += count;
            Volatile.Write(ref _read, readLocal);
            return true;
        }

        private void CopySamples(float[] dest, int destOffset, int readLocal, int count)
        {
            for (int i = 0; i < count; i++)
                dest[destOffset + i] = _buffer[Wrap(readLocal + i)];
        }

        private void CopyWithFadeOut(float[] dest, int destOffset, int readLocal, int haveSamples)
        {
            CopySamples(dest, destOffset, readLocal, haveSamples);

            int fade = Math.Min(_fadeSamples, haveSamples);
            for (int i = 0; i < fade; i++)
            {
                double t = (i + 1) / (double)(fade + 1);
                float w = (float)((Math.Cos(t * Math.PI) + 1.0) * 0.5); // 1 -> 0
                dest[destOffset + haveSamples - fade + i] *= w;
            }
        }

        private void ApplyFadeIn(float[] dest, int destOffset, int fade)
        {
            for (int i = 0; i < fade; i++)
            {
                double t = (i + 1) / (double)(fade + 1);
                float w = 1f - (float)((Math.Cos(t * Math.PI) + 1.0) * 0.5); // 0 -> 1
                dest[destOffset + i] *= w;
            }
        }

        private void ApplyJumpCrossfade(int fromIndex, int toIndex, int fade)
        {
            for (int i = 0; i < fade; i++)
            {
                double t = (i + 1) / (double)(fade + 1);
                float fadeOutW = (float)((Math.Cos(t * Math.PI) + 1.0) * 0.5); // terk edilen bölge ağırlığı: 1 -> 0
                float fadeInW = 1f - fadeOutW; // kalan bölge ağırlığı: 0 -> 1

                float abandoned = _buffer[Wrap(fromIndex + i)];
                int newIdx = Wrap(toIndex + i);
                _buffer[newIdx] = _buffer[newIdx] * fadeInW + abandoned * fadeOutW;
            }
        }

        private int Wrap(int index)
        {
            int m = index % _capacity;
            return m < 0 ? m + _capacity : m;
        }
    }
}
