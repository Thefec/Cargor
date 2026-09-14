using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Müzik ve ses efekti dosyalarının Unity import ayarlarını süreye göre topluca düzeltir.
///
/// NEDEN: flowmusic.app'ten gelen 3 dakikalık parçalar Unity'nin varsayılanı olan
/// "Decompress On Load" ile import oluyordu — bu modda klip yüklenirken tamamı RAM'e
/// açılıyor (3 dk stereo ≈ 32 MB). 15 parçayla bu yüzlerce MB eder. Uzun müzikte doğru
/// ayar Streaming'dir: diskten parça parça okunur, RAM'de yer kaplamaz.
///
/// Kaynak: plans/ses-tasarimi.md §4.8
///
/// Süre kuralı:
///   >= 30 sn  -> Streaming            (müzik parçaları)
///   5-30 sn   -> Compressed In Memory (cingıllar; anında başlaması gerekir, RAM'i az)
///   <  5 sn   -> Decompress On Load   (kısa SFX; en düşük gecikme)
///
/// Yeni parça eklediğinde tekrar çalıştır — zaten doğru olanlara dokunmaz.
/// </summary>
public static class AudioImportSettingsSetup
{
    /// <summary>Taranacak klasörler. Assets/Music KÖKÜ bilinçli olarak dışarıda:
    /// oradaki 8 eski caz WAV'ı bu turun konusu değil, gereksiz diff üretmesin.</summary>
    private static readonly string[] TaranacakKlasorler =
    {
        // Faz C (müzik sistemi, 2026-09-14): Gameplay/Menu, Resources.LoadAll ile runtime'da
        // okunabilmesi için Assets/Resources/Music/ altına taşındı (bkz.
        // Assets/NewCss/Audio/Music/MusicLibrary.cs). Stinger BURADA taşınmadı — Faz B'nin alanı.
        "Assets/Resources/Music/Gameplay",
        "Assets/Resources/Music/Menu",
        "Assets/Music/Stinger",
        "Assets/Audio/SFX",
    };

    private const float StreamingEsigiSn = 30f;
    private const float BellektenEsigiSn = 5f;
    private const float HedefKalite = 0.7f;

    [MenuItem("Tools/Cargor/Audio/Ses Import Ayarlarini Duzelt")]
    public static void Duzelt()
    {
        Uygula(kuruGosterim: false);
    }

    [MenuItem("Tools/Cargor/Audio/Ses Import Ayarlarini Duzelt (kuru gosterim)")]
    public static void KuruGosterim()
    {
        Uygula(kuruGosterim: true);
    }

    private static void Uygula(bool kuruGosterim)
    {
        var klasorler = new List<string>();
        foreach (var k in TaranacakKlasorler)
        {
            if (AssetDatabase.IsValidFolder(k)) klasorler.Add(k);
            else Debug.LogWarning($"[SesImport] Klasör yok, atlandı: {k}");
        }

        if (klasorler.Count == 0)
        {
            Debug.LogError("[SesImport] Taranacak hiçbir klasör bulunamadı. İşlem yapılmadı.");
            return;
        }

        string[] guidler = AssetDatabase.FindAssets("t:AudioClip", klasorler.ToArray());
        var rapor = new StringBuilder();
        int degisen = 0, zatenDogru = 0, hata = 0;

        foreach (string guid in guidler)
        {
            string yol = AssetDatabase.GUIDToAssetPath(guid);
            var importer = AssetImporter.GetAtPath(yol) as AudioImporter;
            var klip = AssetDatabase.LoadAssetAtPath<AudioClip>(yol);

            if (importer == null || klip == null)
            {
                Debug.LogError($"[SesImport] Okunamadı: {yol}");
                hata++;
                continue;
            }

            float sure = klip.length;
            AudioClipLoadType hedefTip =
                sure >= StreamingEsigiSn ? AudioClipLoadType.Streaming :
                sure >= BellektenEsigiSn ? AudioClipLoadType.CompressedInMemory :
                                           AudioClipLoadType.DecompressOnLoad;

            AudioImporterSampleSettings ayar = importer.defaultSampleSettings;

            bool fark = ayar.loadType != hedefTip
                        || ayar.compressionFormat != AudioCompressionFormat.Vorbis
                        || !Mathf.Approximately(ayar.quality, HedefKalite)
                        || ayar.preloadAudioData;

            if (!fark)
            {
                zatenDogru++;
                continue;
            }

            rapor.AppendLine(
                $"  {System.IO.Path.GetFileName(yol)}  ({sure:F0} sn)  " +
                $"{ayar.loadType} -> {hedefTip}, kalite {ayar.quality:F2} -> {HedefKalite:F2}");

            if (!kuruGosterim)
            {
                ayar.loadType = hedefTip;
                ayar.compressionFormat = AudioCompressionFormat.Vorbis;
                ayar.quality = HedefKalite;
                ayar.preloadAudioData = false;

                importer.defaultSampleSettings = ayar;
                importer.loadInBackground = hedefTip == AudioClipLoadType.Streaming;
                importer.SaveAndReimport();
            }

            degisen++;
        }

        string baslik = kuruGosterim ? "[SesImport] KURU GÖSTERİM (hiçbir şey yazılmadı)" : "[SesImport] TAMAM";
        Debug.Log($"{baslik}\n" +
                  $"Taranan: {guidler.Length} · Değişen: {degisen} · Zaten doğru: {zatenDogru} · Hata: {hata}\n" +
                  (rapor.Length > 0 ? rapor.ToString() : "  (değişiklik yok)"));
    }
}
