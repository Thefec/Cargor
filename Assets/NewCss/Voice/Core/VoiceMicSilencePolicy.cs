namespace NewCss.Voice.Core
{
    /// <summary>
    /// "PTT basılı ama Steam hiç ses vermiyor" durumunun ne zaman kabul edileceğine ve ne kadar
    /// süreyle yakalamanın kısa devre yapılacağına karar verir.
    ///
    /// NEDEN COOLDOWN, KALICI KİLİT DEĞİL — 2026-08-09 teşhisi: eski kural "1.5 sn + 0 bayt =
    /// mikrofon yok, oturum sonuna kadar bir daha deneme" idi. İki sorunu vardı:
    ///   (1) Steam'in gürültü kapısı (SteamVoiceSettings.noiseGateLevel) eşiğin altındaki sesi HİÇ
    ///       iletmiyor → SESSİZCE PTT'ye basıp bırakan oyuncu, mikrofonu gayet çalışırken kilitleniyordu.
    ///   (2) "Donanım sorunu kendiliğinden düzelmez" varsayımı yanlıştı: kablosuz kulaklığını açan,
    ///       dongle'ı takan veya Steam ayarını düzelten oyuncu için yakalama ASLA geri gelmiyordu —
    ///       o oturumun tamamı telsizsiz geçiyordu. (Gerçek olayda kök neden tam olarak buydu.)
    /// Yeni kural: baskı GEÇİCİ. <see cref="RetryCooldownSeconds"/> sonra bir sonraki PTT basışında
    /// yeniden denenir; uyarı yine de oturum başına BİR KEZ loglanır (Console spam'i olmasın).
    ///
    /// NEDEN HÂLÂ BİR BASKI VAR: <c>SteamUser.VoiceRecord</c> süreç-global ve mikrofonu işletim
    /// sistemi seviyesinde fiziksel olarak açıyor. Gerçekten mikrofonsuz bir makinede her PTT
    /// basışında mikrofonu açıp 1.5 sn bekleyip aynı sonuca varmak hem israf hem de gereksiz
    /// "mikrofon kullanılıyor" göstergesi demek.
    ///
    /// SAF MANTIK: motor referansı yok, zaman parametre olarak gelir → EditMode'da test edilebilir.
    /// Tek thread'den (ana thread) kullanılmak üzere tasarlandı, kilit içermez.
    /// </summary>
    public sealed class VoiceMicSilencePolicy
    {
        /// <summary>Bir burst'te bu kadar süre boyunca TEK BAYT gelmezse "ses algılanmıyor" kabul edilir.</summary>
        public const double SilenceThresholdSeconds = 1.5;

        /// <summary>Baskının kendiliğinden kalkması için geçmesi gereken süre. Bu süre dolduğunda
        /// bir sonraki PTT basışı mikrofonu normal şekilde yeniden dener.</summary>
        public const double RetryCooldownSeconds = 30.0;

        private double _retryAtTime;

        /// <summary>Şu an yakalama kısa devre mi? HUD bunu okuyup "Ses algılanmıyor" gösterir.</summary>
        public bool IsSuppressed { get; private set; }

        /// <summary>Uyarı bu oturumda loglandı mı — ikinci kez loglanmaz.</summary>
        public bool HasWarnedThisSession { get; private set; }

        /// <summary>Steam bayt verdi: sorun (varsa) çözülmüş, cooldown'un bitmesini bekleme.</summary>
        public void NoteDataReceived()
        {
            IsSuppressed = false;
        }

        /// <summary>
        /// Burst içinde hâlâ tek bayt gelmemişken çağrılır.
        /// <paramref name="burstElapsedSeconds"/> burst başından beri geçen süre.
        /// </summary>
        /// <returns>TRUE ise çağıran uyarıyı LOGLAMALI — oturumda yalnızca ilk tespitte true döner.</returns>
        public bool NoteSilentSample(double now, double burstElapsedSeconds)
        {
            if (burstElapsedSeconds < SilenceThresholdSeconds) return false;

            IsSuppressed = true;
            _retryAtTime = now + RetryCooldownSeconds;

            if (HasWarnedThisSession) return false;
            HasWarnedThisSession = true;
            return true;
        }

        /// <summary>
        /// Her Tick'te sorulur: PTT yok sayılsın mı? Cooldown dolmuşsa baskıyı KALDIRIR ve false döner
        /// (sorgu bilerek yan etkili — "süresi dolmuş baskı"yı ayrıca temizlemeyi unutmak mümkün olmasın).
        /// </summary>
        public bool ShouldIgnorePtt(double now)
        {
            if (!IsSuppressed) return false;
            if (now < _retryAtTime) return true;

            IsSuppressed = false; // cooldown doldu — bir sonraki PTT yeniden denesin
            return false;
        }

        /// <summary>Temiz sayfa (dev aracı / yeniden yapılandırma): uyarı bayrağı dahil her şey sıfırlanır.</summary>
        public void Reset()
        {
            IsSuppressed = false;
            HasWarnedThisSession = false;
            _retryAtTime = 0.0;
        }
    }
}
