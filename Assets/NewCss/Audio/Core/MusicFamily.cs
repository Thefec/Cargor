namespace NewCss.Audio
{
    /// <summary>
    /// Müzik "aile"leri — plans/ses-tasarimi.md §4.8 klasör yapısıyla birebir eşleşir
    /// (bkz. MusicLibrary.ResourcePathFor için Assets/NewCss/Audio/Music/MusicLibrary.cs).
    /// Stinger (iflas/kazanma cingılları) BİLEREK burada YOK — Faz B'nin alanı, bu enum'a
    /// eklenmez (bkz. plans/ses-tasarimi.md Faz C brief, "Stinger sen playlist'ine ALMA").
    /// </summary>
    public enum MusicFamily
    {
        Menu,
        Main,
        Busy,
        Tension,
        Closing
    }
}
