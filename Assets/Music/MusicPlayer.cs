using UnityEngine;

/// <summary>
/// UYUMLULUK KATMANI (2026-09-14, Faz C tamamlandı): Bu bileşen artık MÜZİK ÇALMIYOR — gerçek
/// menü/gameplay müziği artık NewCss.Audio.MusicDirector tarafından yönetiliyor (bkz.
/// Assets/NewCss/Audio/Music/MusicDirector.cs, [RuntimeInitializeOnLoadMethod] bootstrap; hiçbir
/// sahneye elle eklenmeden kendi DontDestroyOnLoad AudioSource'larını kurar).
///
/// SİLİNMEDİ — sadece pasifleştirildi. İki bağımlılık hâlâ bu TİPE ve bu GameObject'in
/// AudioSource'una referans veriyor, ikisi de KIRILMAMALI:
///   1. Assets/Editor/AudioSourceRoutingSetup.cs:61 — GetComponent&lt;MusicPlayer&gt;() != null
///      ile "hangi AudioSource müzik" tespiti yapıyor (Faz A mixer yönlendirmesi).
///   2. Assets/MENUUI/UnifiedSettingsManager.cs:180 — musicAudioSource alanı MainMenu.unity'de
///      bu GameObject'in AudioSource'una serialize edilmiş (satır ~1196-1199 volume=1 yazıyor;
///      kaynak artık hiç çalmadığı için bu artık sessiz bir no-op, zararsız).
///
/// Bu yüzden: sınıf adı, GameObject, üzerindeki AudioSource component'i ve [Serialize] alanları
/// (allowedScenes/stopOtherAudioSources) AYNEN duruyor — sahne dosyasına dokunulmadı. Tek
/// değişen: Awake() artık SceneManager.sceneLoaded'a abone olmuyor, audioSource.Play()/Stop()
/// çağırmıyor. Eskiden burada çalan Ana Menü müziği artık MusicDirector'ın Menu ailesinden
/// geliyor — ikisi aynı anda aktif kalıp üst üste binmesin diye bu bileşen kasıtlı olarak inert.
/// </summary>
public class MusicPlayer : MonoBehaviour
{
    [Header("Müzik Ayarları (ARTIK KULLANILMIYOR — geriye dönük uyumluluk için duruyor)")]
    [Tooltip("KULLANILMIYOR: müzik artık MusicDirector tarafından yönetiliyor (bkz. sınıf başı yorum)")]
    public string[] allowedScenes = { "MainMenu" };

    [Header("Diğer AudioSource Kontrolü (ARTIK KULLANILMIYOR)")]
    [Tooltip("KULLANILMIYOR")]
    public bool stopOtherAudioSources = true;

    private void Awake()
    {
        // Kendi başına ikinci bir müzik motoru koşmasın — mevcut AudioSource'u sessizce
        // durdur/pasifleştir ve bırak. MusicDirector zaten kendi kaynaklarını kuruyor.
        var audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            Debug.LogWarning("[MusicPlayer] Bu GameObject'te AudioSource yok — " +
                              "UnifiedSettingsManager.musicAudioSource referansı geçersiz kalabilir.");
            return;
        }

        audioSource.playOnAwake = false;
        if (audioSource.isPlaying) audioSource.Stop();
    }
}
