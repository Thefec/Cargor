namespace NewCss.Voice.Core
{
    /// <summary>
    /// Bir paket geldiğinde ne yapılacağının kararı. "Dropped" hariç hepsi ses verisinin
    /// çalma tarafına (ring buffer) iletildiği anlamına gelir.
    /// </summary>
    public enum VoicePacketDecision
    {
        Accept,
        Duplicate,
        Reordered,
        Dropped,
    }

    /// <summary>
    /// Tek bir konuşmacı slotu için sequence takibi: dup/reorder/gap kararı + burst canlılık takibi.
    ///
    /// `FacepunchTransport` `UnreliableSequenced`'i düz `Unreliable`'a çeviriyor (plan kanıtı:
    /// FacepunchTransport.cs:110-121) — yani NGO'nun kendi sıralaması bize gelmiyor, sequence
    /// takibini kendimiz yapmak ZORUNDAYIZ.
    ///
    /// uint16 sarmalamasını (65535 -> 0) doğru okumak için İŞARETLİ fark kullanılır. Naif bir
    /// `sequence > lastAccepted` karşılaştırması sarmada kırılır: 65535'ten sonra gelen 0, "0 < 65535"
    /// olduğu için geriye gitmiş gibi görünür ama aslında bir sonraki pakettir. `(short)(a - b)`
    /// farkı, ushort aritmetiğinin dairesel yapısını işaretli bir sayıya doğru şekilde yansıtır.
    /// </summary>
    public sealed class VoiceSequenceTracker
    {
        private readonly int _reorderWindow;
        private readonly double _burstTimeoutSeconds;

        private bool _hasSequence;
        private ushort _lastAccepted;
        private double _lastPacketTime;
        private bool _burstActive;

        /// <param name="reorderWindow">
        /// Geçmişe doğru kaç sequence adımına kadar "sıra bozukluğu, ama hâlâ kabul" sayılır.
        /// Bunun dışına düşen paketler çok eski kabul edilip düşürülür.
        /// </param>
        /// <param name="burstTimeoutSeconds">
        /// BurstEnd bayrağı unreliable teslimatta kaybolabilir (bkz. IsBurstStale) — bu yüzden
        /// akışın canlılığı SADECE bayrağa değil, son paketten geçen süreye de bakılarak karar verilir.
        /// </param>
        public VoiceSequenceTracker(int reorderWindow = 8, double burstTimeoutSeconds = 0.4)
        {
            _reorderWindow = reorderWindow;
            _burstTimeoutSeconds = burstTimeoutSeconds;
        }

        /// <summary>Şu an bir burst'ün ortasında olduğumuza dair en son bilgi (bayrak + zaman aşımı birleşimi).</summary>
        public bool IsBurstActive => _burstActive;

        /// <summary>
        /// BurstEnd bayrağı kaybolduğunda akışı sonsuza kadar "aktif" sanmamak için zorunlu zaman
        /// aşımı yolu. Çağıran taraf periyodik olarak (örn. her frame) bunu sorup true dönerse
        /// ResetBurst() çağırıp slotu serbest bırakmalı.
        /// </summary>
        public bool IsBurstStale(double nowSeconds)
        {
            if (!_burstActive) return false;
            return (nowSeconds - _lastPacketTime) >= _burstTimeoutSeconds;
        }

        /// <summary>
        /// Gelen paket için dup/reorder/gap kararı verir ve iç durumu günceller.
        /// `nowSeconds` çağıranın verdiği zaman damgasıdır (bu sınıf asla saat okumaya çalışmaz —
        /// noEngineReferences altında Time.* zaten yok, ama saf/test edilebilir olmak için de
        /// zamanı parametre almak şart).
        /// </summary>
        public VoicePacketDecision Evaluate(ushort sequence, VoicePacketFlags flags, double nowSeconds)
        {
            bool isBurstStart = (flags & VoicePacketFlags.BurstStart) != 0;

            if (isBurstStart || !_hasSequence)
            {
                // Yeni burst (ya da elimizde hiç referans yoksa ilk gördüğümüz paket): sequence
                // tabanını buradan resetle. BurstEnd sonrası yeni bir burst başladığında sequence
                // sarmalamadan bağımsız olarak sıfırdan (veya rastgele bir tabandan) başlayabilir —
                // diff hesabına güvenmek yerine burada koşulsuz kabul ediyoruz.
                _hasSequence = true;
                _lastAccepted = sequence;
                _lastPacketTime = nowSeconds;
                _burstActive = (flags & VoicePacketFlags.BurstEnd) == 0;
                return VoicePacketDecision.Accept;
            }

            short diff = (short)(sequence - _lastAccepted);
            VoicePacketDecision decision;

            if (diff == 0)
            {
                decision = VoicePacketDecision.Duplicate;
            }
            else if (diff > 0)
            {
                // İleri: sıradaki ya da paket kaybından doğan bir gap — unreliable akışta gap
                // normaldir, beklemeden kabul edip ilerletiyoruz (ses gecikmesi jitter buffer'ın işi).
                _lastAccepted = sequence;
                decision = VoicePacketDecision.Accept;
            }
            else
            {
                // Geçmişten gelen paket: pencere içiyse kabul (ama lastAccepted'ı GERİ ALMA —
                // sıra bozukluğu ilerlemeyi durdurmaz), pencere dışıysa çok eski, düşür.
                decision = (-diff <= _reorderWindow) ? VoicePacketDecision.Reordered : VoicePacketDecision.Dropped;
            }

            if (decision != VoicePacketDecision.Dropped)
            {
                _lastPacketTime = nowSeconds;
                _burstActive = (flags & VoicePacketFlags.BurstEnd) == 0;
            }

            return decision;
        }

        /// <summary>
        /// Zaman aşımı (IsBurstStale) veya BurstEnd sonrası çağrılır: bir sonraki paket, önceki
        /// burst'ün sequence tabanına göre değil sıfırdan (koşulsuz Accept ile) değerlendirilir.
        /// </summary>
        public void ResetBurst()
        {
            _hasSequence = false;
            _burstActive = false;
        }
    }
}
