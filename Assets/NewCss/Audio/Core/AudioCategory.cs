namespace NewCss.Audio
{
    /// <summary>
    /// Ses yönlendirme kategorileri. Faz A itibarıyla sadece Music/SFX var (bkz.
    /// plans/ses-tasarimi.md §1 — atmosfer katmanı ve telsiz kasıtlı olarak burada YOK;
    /// telsiz kendi ayrı ses zincirinde çalışıyor, bkz. RadioVoicePrefs sınıf yorumu).
    /// </summary>
    public enum AudioCategory
    {
        Music,
        SFX
    }
}
