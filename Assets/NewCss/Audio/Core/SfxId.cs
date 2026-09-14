namespace NewCss.Audio
{
    /// <summary>
    /// Faz B (bkz. plans/ses-tasarimi.md §3) — olay/etkileşim sesi kimlikleri. SfxLibrary
    /// (Resources/Audio/SfxLibrary.asset) bunu AudioClip'e çözer; SfxBus bu ID'lerle çağrılır.
    /// Yeni bir olay sesi eklerken: (1) buraya ekle, (2) Assets/Editor/SfxLibrarySetup.cs'deki
    /// tabloya ekle veya Inspector'dan elle ata, (3) AudioSlotValidator boş kalırsa GÜRÜLTÜLÜ uyarır.
    ///
    /// MoneyEarned ve CorrectItem: klip henüz SEÇİLMEDİ (plan §3.2 — kullanıcı karar verecek).
    /// Bağlantı noktaları hazır, SfxLibrary'de bilerek BOŞ slot olarak duruyor.
    /// </summary>
    public enum SfxId
    {
        CounterTick,
        DayEnd,
        QuestComplete,
        WrongItem,
        RentWarning,
        UpgradeBought,
        Bankrupt,
        Victory,

        // Klip henüz seçilmedi (plan §3.2 AÇIK) — AudioSlotValidator bunları boş slot olarak raporlar.
        MoneyEarned,
        CorrectItem,
    }
}
